// MIT licensed; Windows embedding adapter for the upstream libghostty-vt API.
#include <ghostty/vt.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>
#define API __declspec(dllexport)
typedef struct { uint32_t fg,bg,flags,offset,length; } WinCell;
// All borrowed image pointers are consumed under the host's terminal lock.
typedef struct {
 uint32_t width,height,format; int32_t z,col,row;
 uint32_t pixel_width,pixel_height,offset_x,offset_y,source_x,source_y,source_width,source_height;
 uint64_t generation; const uint8_t *pixels; size_t length; uint32_t image_id,placement_id;
} WinImage;
_Static_assert(sizeof(WinImage)==88,"WinImage ABI changed");
typedef int (*WinPngDecode)(const GhosttyAllocator*,const uint8_t*,size_t,GhosttySysImage*);
typedef void (*WinHoldSnapshot)(void*);
static WinPngDecode png_decoder;
static bool decode_png(void *userdata,const GhosttyAllocator *a,const uint8_t *p,size_t n,GhosttySysImage *out) {
 (void)userdata;return png_decoder && png_decoder(a,p,n,out)!=0;
}
API void gw_initialize(WinPngDecode decoder) {
 png_decoder=decoder;ghostty_sys_set(GHOSTTY_SYS_OPT_DECODE_PNG,decoder?(const void*)decode_png:NULL);
}
API void *gw_image_alloc(const GhosttyAllocator *a,size_t n){return ghostty_alloc(a,n);}
API void gw_image_free(const GhosttyAllocator *a,void *p,size_t n){ghostty_free(a,p,n);}
typedef struct {
 GhosttyTerminal term; GhosttyRenderState render;
 GhosttyRenderStateRowIterator rows; GhosttyRenderStateRowCells cells;
 GhosttyKeyEncoder encoder; GhosttyKeyEvent event; GhosttySearch search;
 GhosttyMouseEncoder mouse_encoder; GhosttyMouseEvent mouse_event;
 GhosttySelectionGesture gesture; GhosttySelectionGestureEvent gesture_events[3];
 WinCell *frame; size_t frame_cap; unsigned char *text; size_t text_cap,text_len;
 unsigned char *replies; size_t reply_cap,reply_len;
 bool held; ULONGLONG hold_started;
 GhosttyKittyGraphics graphics; GhosttyKittyGraphicsPlacementIterator images;
 WinHoldSnapshot hold_snapshot;void *hold_userdata;
} WinTerminal;
static int reserve(void **p,size_t *cap,size_t n) {
 if(n<=*cap)return 1; size_t next=n+4096; void *q=realloc(*p,next);
 if(!q)return 0; *p=q;*cap=next;return 1;
}
static void reply(GhosttyTerminal t,void *u,const uint8_t *data,size_t n) {
 (void)t; WinTerminal *w=u;
 if(n>1024*1024 || w->reply_len>1024*1024-n)return;
 if(reserve((void**)&w->replies,&w->reply_cap,w->reply_len+n)) {
 memcpy(w->replies+w->reply_len,data,n);w->reply_len+=n; }
}
static void render_hold(GhosttyTerminal t,void *u,bool held) {
 WinTerminal *w=u;if(held){ghostty_render_state_update(w->render,t);w->hold_started=GetTickCount64();if(w->hold_snapshot)w->hold_snapshot(w->hold_userdata);}w->held=held;
}
API void gw_free(WinTerminal *w) {
 if(!w)return;
 for(int i=0;i<3;i++)ghostty_selection_gesture_event_free(w->gesture_events[i]);
 ghostty_selection_gesture_free(w->gesture,w->term);
 ghostty_kitty_graphics_placement_iterator_free(w->images);
 ghostty_mouse_event_free(w->mouse_event);ghostty_mouse_encoder_free(w->mouse_encoder);
 ghostty_search_free(w->search);ghostty_key_event_free(w->event);ghostty_key_encoder_free(w->encoder);
 ghostty_render_state_row_cells_free(w->cells);ghostty_render_state_row_iterator_free(w->rows);
 ghostty_render_state_free(w->render);ghostty_terminal_free(w->term);
 free(w->frame);free(w->text);free(w->replies);free(w);
}
API WinTerminal *gw_new(int cols,int rows) {
 WinTerminal *w=calloc(1,sizeof(*w));if(!w)return NULL;
 if(ghostty_terminal_new(NULL,&w->term,cols,rows) ||
 ghostty_render_state_new(NULL,&w->render) ||
 ghostty_render_state_row_iterator_new(NULL,&w->rows) ||
 ghostty_render_state_row_cells_new(NULL,&w->cells) ||
 ghostty_key_encoder_new(NULL,&w->encoder) || ghostty_key_event_new(NULL,&w->event) || ghostty_search_new(NULL,&w->search,w->term) ||
 ghostty_mouse_encoder_new(NULL,&w->mouse_encoder) || ghostty_mouse_event_new(NULL,&w->mouse_event) ||
 ghostty_kitty_graphics_placement_iterator_new(NULL,&w->images)) {gw_free(w);return NULL;}
 if(ghostty_selection_gesture_new(NULL,&w->gesture)){gw_free(w);return NULL;}
 for(int i=0;i<3;i++)if(ghostty_selection_gesture_event_new(NULL,&w->gesture_events[i],(GhosttySelectionGestureEventType)i)){gw_free(w);return NULL;}
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_USERDATA,w);
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_WRITE_PTY,(const void*)reply);
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_RENDER_HOLD,(const void*)render_hold);
 bool pull=false;ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_RESIZE_PULL_SCROLLBACK,&pull);
 size_t history=64*1024*1024;ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SCROLLBACK_MAX_BYTES,&history);
 uint64_t image_limit=64*1024*1024;
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_KITTY_IMAGE_STORAGE_LIMIT,&image_limit);
 return w;
}
API void gw_hold_snapshot(WinTerminal *w,WinHoldSnapshot callback,void *userdata) {w->hold_snapshot=callback;w->hold_userdata=userdata;}
API int gw_images_begin(WinTerminal *w) {
 w->graphics=NULL;
 if(ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_KITTY_GRAPHICS,&w->graphics)!=GHOSTTY_SUCCESS || !w->graphics)return 0;
 return ghostty_kitty_graphics_get(w->graphics,GHOSTTY_KITTY_GRAPHICS_DATA_PLACEMENT_ITERATOR,&w->images)==GHOSTTY_SUCCESS;
}
API int gw_images_next(WinTerminal *w,WinImage *out) {
 while(ghostty_kitty_graphics_placement_next(w->images)) {
  memset(out,0,sizeof(*out));uint32_t id=0;bool is_virtual=false;
  ghostty_kitty_graphics_placement_get(w->images,GHOSTTY_KITTY_GRAPHICS_PLACEMENT_DATA_IMAGE_ID,&id);
  ghostty_kitty_graphics_placement_get(w->images,GHOSTTY_KITTY_GRAPHICS_PLACEMENT_DATA_IS_VIRTUAL,&is_virtual);
  if(is_virtual)continue; // Unicode placeholder placement requires a separate cell traversal.
  GhosttyKittyGraphicsImage image=ghostty_kitty_graphics_image(w->graphics,id);if(!image)continue;
  GhosttyKittyGraphicsPlacementRenderInfo info=GHOSTTY_INIT_SIZED(GhosttyKittyGraphicsPlacementRenderInfo);
  if(ghostty_kitty_graphics_placement_render_info(w->images,image,w->term,&info)!=GHOSTTY_SUCCESS || !info.viewport_visible)continue;
  if(ghostty_kitty_graphics_image_get(image,GHOSTTY_KITTY_IMAGE_DATA_DATA_PTR,&out->pixels)!=GHOSTTY_SUCCESS)continue;
  out->image_id=id;
  ghostty_kitty_graphics_placement_get(w->images,GHOSTTY_KITTY_GRAPHICS_PLACEMENT_DATA_PLACEMENT_ID,&out->placement_id);
  ghostty_kitty_graphics_placement_get(w->images,GHOSTTY_KITTY_GRAPHICS_PLACEMENT_DATA_Z,&out->z);
  ghostty_kitty_graphics_placement_get(w->images,GHOSTTY_KITTY_GRAPHICS_PLACEMENT_DATA_X_OFFSET,&out->offset_x);
  ghostty_kitty_graphics_placement_get(w->images,GHOSTTY_KITTY_GRAPHICS_PLACEMENT_DATA_Y_OFFSET,&out->offset_y);
  ghostty_kitty_graphics_image_get(image,GHOSTTY_KITTY_IMAGE_DATA_WIDTH,&out->width);
  ghostty_kitty_graphics_image_get(image,GHOSTTY_KITTY_IMAGE_DATA_HEIGHT,&out->height);
  ghostty_kitty_graphics_image_get(image,GHOSTTY_KITTY_IMAGE_DATA_FORMAT,&out->format);
  ghostty_kitty_graphics_image_get(image,GHOSTTY_KITTY_IMAGE_DATA_DATA_LEN,&out->length);
  ghostty_kitty_graphics_image_get(image,GHOSTTY_KITTY_IMAGE_DATA_GENERATION,&out->generation);
  out->col=info.viewport_col;out->row=info.viewport_row;
  out->pixel_width=info.pixel_width;out->pixel_height=info.pixel_height;
  out->source_x=info.source_x;out->source_y=info.source_y;out->source_width=info.source_width;out->source_height=info.source_height;
  return 1;
 }
 return 0;
}
API int gw_resize(WinTerminal *w,int cols,int rows,int cw,int ch) {
 ghostty_selection_gesture_reset(w->gesture,w->term);ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,NULL);
 return ghostty_terminal_resize(w->term,cols,rows,cw,ch);
}
API void gw_write(WinTerminal *w,const uint8_t *p,int len) { ghostty_terminal_vt_write(w->term,p,len); }
API int gw_replies(WinTerminal *w,uint8_t *out,int cap) {
 size_t n=w->reply_len<(size_t)cap?w->reply_len:(size_t)cap;
 if(n){memcpy(out,w->replies,n);memmove(w->replies,w->replies+n,w->reply_len-n);w->reply_len-=n;}return (int)n;
}
API void gw_theme(WinTerminal *w,uint32_t fg,uint32_t bg) {
 GhosttyColorRgb f={(fg>>16)&255,(fg>>8)&255,fg&255},b={(bg>>16)&255,(bg>>8)&255,bg&255};
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_COLOR_FOREGROUND,&f);
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_COLOR_BACKGROUND,&b);
}
API int gw_frame(WinTerminal *w,WinCell **out,uint8_t **text,int *cx,int *cy,int *cursor) {
 if(w->held && GetTickCount64()-w->hold_started>=1000) {
  GhosttyTerminalModeConfig m={.mode=GHOSTTY_MODE_SYNC_OUTPUT,.value=false};
  ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_MODE,&m);w->held=false;
 }
 if(!w->held && ghostty_render_state_update(w->render,w->term))return -1;
 uint16_t cols=0,rows=0;ghostty_render_state_get(w->render,GHOSTTY_RENDER_STATE_DATA_COLS,&cols);
 ghostty_render_state_get(w->render,GHOSTTY_RENDER_STATE_DATA_ROWS,&rows);
 size_t count=(size_t)cols*rows;
 if(!reserve((void**)&w->frame,&w->frame_cap,count*sizeof(WinCell)))return -1;
 GhosttyRenderStateColors colors=GHOSTTY_INIT_SIZED(GhosttyRenderStateColors);
 ghostty_render_state_get(w->render,GHOSTTY_RENDER_STATE_DATA_COLORS,&colors);
 GhosttyRenderStateCursor cur=GHOSTTY_INIT_SIZED(GhosttyRenderStateCursor);
 ghostty_render_state_get(w->render,GHOSTTY_RENDER_STATE_DATA_CURSOR,&cur);
 *cx=cur.viewport_has_value?cur.viewport_x:0;*cy=cur.viewport_has_value?cur.viewport_y:0;
 *cursor=(cur.viewport_has_value && cur.visible)?1+(int)cur.visual_style:0;
 w->text_len=0;size_t index=0;
 ghostty_render_state_get(w->render,GHOSTTY_RENDER_STATE_DATA_ROW_ITERATOR,&w->rows);
 while(ghostty_render_state_row_iterator_next(w->rows)) {
  GhosttyRow raw_row;bool wrapped=false,continuation=false;
  ghostty_render_state_row_get(w->rows,GHOSTTY_RENDER_STATE_ROW_DATA_RAW,&raw_row);
  ghostty_row_get(raw_row,GHOSTTY_ROW_DATA_WRAP,&wrapped);
  ghostty_row_get(raw_row,GHOSTTY_ROW_DATA_WRAP_CONTINUATION,&continuation);
  ghostty_render_state_row_get(w->rows,GHOSTTY_RENDER_STATE_ROW_DATA_CELLS,&w->cells);
  while(ghostty_render_state_row_cells_next(w->cells) && index<count) {
   WinCell *c=&w->frame[index++];memset(c,0,sizeof(*c));
   GhosttyColorRgb fg=colors.foreground,bg=colors.background;
   ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_FG_COLOR,&fg);
   ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_BG_COLOR,&bg);
   GhosttyStyle s=GHOSTTY_INIT_SIZED(GhosttyStyle);
   ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_STYLE,&s);
   bool selected=false;ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_SELECTED,&selected);
   c->fg=(fg.r<<16)|(fg.g<<8)|fg.b;c->bg=(bg.r<<16)|(bg.g<<8)|bg.b;
   if(s.inverse){uint32_t temp=c->fg;c->fg=c->bg;c->bg=temp;}
   c->flags=s.bold | (s.italic<<1) | ((s.underline!=0)<<2) | (s.strikethrough<<3) | (s.invisible<<4) | (selected<<5) | (s.faint<<6);
   GhosttyCell raw_cell;GhosttyCellWide wide=GHOSTTY_CELL_WIDE_NARROW;
   ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_RAW,&raw_cell);
   ghostty_cell_get(raw_cell,GHOSTTY_CELL_DATA_WIDE,&wide);
   c->flags|=(wrapped?128:0)|(wide==GHOSTTY_CELL_WIDE_SPACER_TAIL?256:0)|(wide==GHOSTTY_CELL_WIDE_SPACER_HEAD?512:0)|(continuation?1024:0);
   GhosttyBuffer buf={0};ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_GRAPHEMES_UTF8,&buf);
   if(buf.len) {
    if(!reserve((void**)&w->text,&w->text_cap,w->text_len+buf.len))return -1;
    buf.ptr=w->text+w->text_len;buf.cap=buf.len;
    if(ghostty_render_state_row_cells_get(w->cells,GHOSTTY_RENDER_STATE_ROW_CELLS_DATA_GRAPHEMES_UTF8,&buf))return -1;
    c->offset=(uint32_t)w->text_len;c->length=(uint32_t)buf.len;w->text_len+=buf.len;
   }
  }
 }
 ghostty_render_state_clean(w->render);*out=w->frame;*text=w->text;return (int)index;
}
API void gw_scroll(WinTerminal *w,int delta) {
 GhosttyTerminalScrollViewport b={.tag=delta==0?GHOSTTY_SCROLL_VIEWPORT_BOTTOM:GHOSTTY_SCROLL_VIEWPORT_DELTA};
 b.value.delta=delta;ghostty_terminal_scroll_viewport(w->term,b);
}
API int gw_title(WinTerminal *w,char *out,int cap) {
 GhosttyString s={0};ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_TITLE,&s);
 size_t n=s.len<(size_t)cap?s.len:(size_t)cap;if(n)memcpy(out,s.ptr,n);return (int)n;
}
API int gw_pwd(WinTerminal *w,char *out,int cap) {
 GhosttyString s={0};ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_PWD,&s);
 if(s.len>INT_MAX)return -1;
 if(s.len && out && s.len<=(size_t)cap)memcpy(out,s.ptr,s.len);return (int)s.len;
}
API void gw_select(WinTerminal *w,int x1,int y1,int x2,int y2) {
 ghostty_selection_gesture_reset(w->gesture,w->term);
 if(x1<0){ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,NULL);return;}
 if(y1>y2 || (y1==y2 && x1>x2)){int q=x1;x1=x2;x2=q;q=y1;y1=y2;y2=q;}
 GhosttyPoint a={.tag=GHOSTTY_POINT_TAG_VIEWPORT,.value.coordinate={x1,y1}},b={.tag=GHOSTTY_POINT_TAG_VIEWPORT,.value.coordinate={x2,y2}};
 GhosttySelection selection=GHOSTTY_INIT_SIZED(GhosttySelection);
 selection.start=(GhosttyGridRef)GHOSTTY_INIT_SIZED(GhosttyGridRef);selection.end=(GhosttyGridRef)GHOSTTY_INIT_SIZED(GhosttyGridRef);
 if(ghostty_terminal_grid_ref(w->term,a,&selection.start) || ghostty_terminal_grid_ref(w->term,b,&selection.end))return;
 ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,&selection);
}
API int gw_select_all(WinTerminal *w) {
 ghostty_selection_gesture_reset(w->gesture,w->term);
 GhosttySelection s=GHOSTTY_INIT_SIZED(GhosttySelection);
 int r=ghostty_terminal_select_all(w->term,&s);
 return r==GHOSTTY_SUCCESS?ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,&s):r==GHOSTTY_NO_VALUE?0:r;
}
API int gw_gesture(WinTerminal *w,int action,int x,int y,double px,double py,int cw,int height,int rectangle,uint64_t time_ns) {
 if(action<0||action>2||cw<=0||height<=0)return GHOSTTY_INVALID_VALUE;
 GhosttySelectionGestureEvent e=w->gesture_events[action];
 GhosttyGridRef ref=GHOSTTY_INIT_SIZED(GhosttyGridRef);
 GhosttyPoint p={.tag=GHOSTTY_POINT_TAG_VIEWPORT,.value.coordinate={x,y}};
 int r=ghostty_terminal_grid_ref(w->term,p,&ref);if(r)return r;
 if((r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_REF,&ref)))return r;
 if(action!=1){
  GhosttySurfacePosition position={.x=px,.y=py};
  if((r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_POSITION,&position)))return r;
 }
 if(action==0){
  uint64_t time=time_ns?time_ns:GetTickCount64()*1000000,interval=(uint64_t)GetDoubleClickTime()*1000000;
  double distance=GetSystemMetrics(SM_CXDOUBLECLK);
  if((r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_TIME_NS,&time)) ||
   (r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_REPEAT_INTERVAL_NS,&interval)) ||
   (r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_REPEAT_DISTANCE,&distance)))return r;
 }
 if(action==2){
  uint16_t cols=0;ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_COLS,&cols);
  GhosttySelectionGestureGeometry geometry={.columns=cols,.cell_width=cw,.padding_left=0,.screen_height=height};bool rect=rectangle!=0;
  if((r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_GEOMETRY,&geometry)) ||
   (r=ghostty_selection_gesture_event_set(e,GHOSTTY_SELECTION_GESTURE_EVENT_OPT_RECTANGLE,&rect)))return r;
 }
 GhosttySelection s=GHOSTTY_INIT_SIZED(GhosttySelection);
 r=ghostty_selection_gesture_event(w->gesture,w->term,e,&s);
 if(r==GHOSTTY_SUCCESS)return ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,&s);
 if(r==GHOSTTY_NO_VALUE && action==0)ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,NULL);
 return r==GHOSTTY_NO_VALUE?0:r;
}
API int gw_text(WinTerminal *w,int selection,char *out,int cap) {
 GhosttySelection current=GHOSTTY_INIT_SIZED(GhosttySelection);
 if(selection && ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_SELECTION,&current)!=GHOSTTY_SUCCESS)return 0;
 GhosttyFormatterTerminalOptions o=GHOSTTY_INIT_SIZED(GhosttyFormatterTerminalOptions);
 o.emit=GHOSTTY_FORMATTER_FORMAT_PLAIN;o.trim=true;o.unwrap=true;o.selection=selection?&current:NULL;
 GhosttyFormatter f=NULL;if(ghostty_formatter_terminal_new(NULL,&f,w->term,o))return -1;
 size_t n=0;ghostty_formatter_format_buf(f,(uint8_t*)out,cap,&n);ghostty_formatter_free(f);return n>2147483647?-1:(int)n;
}
static GhosttyKey key(int vk) {
 if(vk>='A'&&vk<='Z')return GHOSTTY_KEY_A+(vk-'A');
 if(vk>='0'&&vk<='9')return GHOSTTY_KEY_DIGIT_0+(vk-'0');
 if(vk>=0x70&&vk<=0x87)return GHOSTTY_KEY_F1+(vk-0x70);
 if(vk>=0x60&&vk<=0x69)return GHOSTTY_KEY_NUMPAD_0+(vk-0x60);
 switch(vk) {
  case 8:return GHOSTTY_KEY_BACKSPACE;case 9:return GHOSTTY_KEY_TAB;case 13:return GHOSTTY_KEY_ENTER;
  case 27:return GHOSTTY_KEY_ESCAPE;case 32:return GHOSTTY_KEY_SPACE;case 33:return GHOSTTY_KEY_PAGE_UP;
  case 34:return GHOSTTY_KEY_PAGE_DOWN;case 35:return GHOSTTY_KEY_END;case 36:return GHOSTTY_KEY_HOME;
  case 37:return GHOSTTY_KEY_ARROW_LEFT;case 38:return GHOSTTY_KEY_ARROW_UP;case 39:return GHOSTTY_KEY_ARROW_RIGHT;
  case 40:return GHOSTTY_KEY_ARROW_DOWN;case 45:return GHOSTTY_KEY_INSERT;case 46:return GHOSTTY_KEY_DELETE;
  case 0xbd:return GHOSTTY_KEY_MINUS;case 0xbb:return GHOSTTY_KEY_EQUAL;
  case 0xdb:return GHOSTTY_KEY_BRACKET_LEFT;case 0xdd:return GHOSTTY_KEY_BRACKET_RIGHT;
  case 0xdc:return GHOSTTY_KEY_BACKSLASH;case 0xba:return GHOSTTY_KEY_SEMICOLON;case 0xde:return GHOSTTY_KEY_QUOTE;
  case 0xbc:return GHOSTTY_KEY_COMMA;case 0xbe:return GHOSTTY_KEY_PERIOD;case 0xbf:return GHOSTTY_KEY_SLASH;
  case 0xc0:return GHOSTTY_KEY_BACKQUOTE;
  case 0x10:case 0xa0:return GHOSTTY_KEY_SHIFT_LEFT;case 0xa1:return GHOSTTY_KEY_SHIFT_RIGHT;
  case 0x11:case 0xa2:return GHOSTTY_KEY_CONTROL_LEFT;case 0xa3:return GHOSTTY_KEY_CONTROL_RIGHT;
  case 0x12:case 0xa4:return GHOSTTY_KEY_ALT_LEFT;case 0xa5:return GHOSTTY_KEY_ALT_RIGHT;
  case 0x5b:return GHOSTTY_KEY_META_LEFT;case 0x5c:return GHOSTTY_KEY_META_RIGHT;
  case 0x5d:return GHOSTTY_KEY_CONTEXT_MENU;case 0x14:return GHOSTTY_KEY_CAPS_LOCK;
  case 0x90:return GHOSTTY_KEY_NUM_LOCK;case 0x91:return GHOSTTY_KEY_SCROLL_LOCK;
  case 0x13:return GHOSTTY_KEY_PAUSE;case 0x2c:return GHOSTTY_KEY_PRINT_SCREEN;
  case 0x6a:return GHOSTTY_KEY_NUMPAD_MULTIPLY;case 0x6b:return GHOSTTY_KEY_NUMPAD_ADD;
  case 0x6c:return GHOSTTY_KEY_NUMPAD_SEPARATOR;case 0x6d:return GHOSTTY_KEY_NUMPAD_SUBTRACT;
  case 0x6e:return GHOSTTY_KEY_NUMPAD_DECIMAL;case 0x6f:return GHOSTTY_KEY_NUMPAD_DIVIDE;
  default:return GHOSTTY_KEY_UNIDENTIFIED;
 }
}
// Set-1 scan codes carry physical identity independently of the active layout.
// E0 is kept in the high byte, including navigation versus NumLock-off keypad.
static GhosttyKey physical_key(int vk,int scan) {
 if(vk==0x13)return GHOSTTY_KEY_PAUSE;
 const char *row=NULL;int index=0;
 if(scan>=0x10&&scan<=0x19){row="QWERTYUIOP";index=scan-0x10;}
 else if(scan>=0x1e&&scan<=0x26){row="ASDFGHJKL";index=scan-0x1e;}
 else if(scan>=0x2c&&scan<=0x32){row="ZXCVBNM";index=scan-0x2c;}
 if(row)return key(row[index]);
 if(scan>=2&&scan<=11)return key(scan==11?'0':'1'+scan-2);
 if(scan>=0x3b&&scan<=0x44)return GHOSTTY_KEY_F1+scan-0x3b;
 switch(scan){
  case 0x01:return GHOSTTY_KEY_ESCAPE;case 0x0c:return GHOSTTY_KEY_MINUS;case 0x0d:return GHOSTTY_KEY_EQUAL;
  case 0x0e:return GHOSTTY_KEY_BACKSPACE;case 0x0f:return GHOSTTY_KEY_TAB;
  case 0x1a:return GHOSTTY_KEY_BRACKET_LEFT;case 0x1b:return GHOSTTY_KEY_BRACKET_RIGHT;
  case 0x1c:return GHOSTTY_KEY_ENTER;case 0xe01c:return GHOSTTY_KEY_NUMPAD_ENTER;
  case 0x1d:return GHOSTTY_KEY_CONTROL_LEFT;case 0xe01d:return GHOSTTY_KEY_CONTROL_RIGHT;
  case 0x27:return GHOSTTY_KEY_SEMICOLON;case 0x28:return GHOSTTY_KEY_QUOTE;case 0x29:return GHOSTTY_KEY_BACKQUOTE;
  case 0x2a:return GHOSTTY_KEY_SHIFT_LEFT;case 0x36:return GHOSTTY_KEY_SHIFT_RIGHT;
  case 0x2b:return GHOSTTY_KEY_BACKSLASH;case 0x33:return GHOSTTY_KEY_COMMA;case 0x34:return GHOSTTY_KEY_PERIOD;
  case 0x35:return GHOSTTY_KEY_SLASH;case 0xe035:return GHOSTTY_KEY_NUMPAD_DIVIDE;
  case 0x37:return GHOSTTY_KEY_NUMPAD_MULTIPLY;case 0xe037:return GHOSTTY_KEY_PRINT_SCREEN;
  case 0x38:return GHOSTTY_KEY_ALT_LEFT;case 0xe038:return GHOSTTY_KEY_ALT_RIGHT;
  case 0x39:return GHOSTTY_KEY_SPACE;case 0x3a:return GHOSTTY_KEY_CAPS_LOCK;
  case 0x45:case 0xe045:return GHOSTTY_KEY_NUM_LOCK;case 0x46:return GHOSTTY_KEY_SCROLL_LOCK;
  case 0x47:return vk==36?GHOSTTY_KEY_NUMPAD_HOME:GHOSTTY_KEY_NUMPAD_7;
  case 0x48:return vk==38?GHOSTTY_KEY_NUMPAD_UP:GHOSTTY_KEY_NUMPAD_8;
  case 0x49:return vk==33?GHOSTTY_KEY_NUMPAD_PAGE_UP:GHOSTTY_KEY_NUMPAD_9;
  case 0x4a:return GHOSTTY_KEY_NUMPAD_SUBTRACT;
  case 0x4b:return vk==37?GHOSTTY_KEY_NUMPAD_LEFT:GHOSTTY_KEY_NUMPAD_4;
  case 0x4c:return vk==12?GHOSTTY_KEY_NUMPAD_BEGIN:GHOSTTY_KEY_NUMPAD_5;
  case 0x4d:return vk==39?GHOSTTY_KEY_NUMPAD_RIGHT:GHOSTTY_KEY_NUMPAD_6;
  case 0x4e:return GHOSTTY_KEY_NUMPAD_ADD;
  case 0x4f:return vk==35?GHOSTTY_KEY_NUMPAD_END:GHOSTTY_KEY_NUMPAD_1;
  case 0x50:return vk==40?GHOSTTY_KEY_NUMPAD_DOWN:GHOSTTY_KEY_NUMPAD_2;
  case 0x51:return vk==34?GHOSTTY_KEY_NUMPAD_PAGE_DOWN:GHOSTTY_KEY_NUMPAD_3;
  case 0x52:return vk==45?GHOSTTY_KEY_NUMPAD_INSERT:GHOSTTY_KEY_NUMPAD_0;
  case 0x53:return vk==46?GHOSTTY_KEY_NUMPAD_DELETE:GHOSTTY_KEY_NUMPAD_DECIMAL;
  case 0xe047:return GHOSTTY_KEY_HOME;case 0xe048:return GHOSTTY_KEY_ARROW_UP;case 0xe049:return GHOSTTY_KEY_PAGE_UP;
  case 0xe04b:return GHOSTTY_KEY_ARROW_LEFT;case 0xe04d:return GHOSTTY_KEY_ARROW_RIGHT;
  case 0xe04f:return GHOSTTY_KEY_END;case 0xe050:return GHOSTTY_KEY_ARROW_DOWN;case 0xe051:return GHOSTTY_KEY_PAGE_DOWN;
  case 0xe052:return GHOSTTY_KEY_INSERT;case 0xe053:return GHOSTTY_KEY_DELETE;
  case 0x56:return GHOSTTY_KEY_INTL_BACKSLASH;case 0x57:return GHOSTTY_KEY_F11;case 0x58:return GHOSTTY_KEY_F12;
  case 0xe05b:return GHOSTTY_KEY_META_LEFT;case 0xe05c:return GHOSTTY_KEY_META_RIGHT;case 0xe05d:return GHOSTTY_KEY_CONTEXT_MENU;
  default:return key(vk);
 }
}
API int gw_key(WinTerminal *w,int vk,int scan,int mods,int action,uint32_t unshifted,int consumed,int composing,const char *utf8,int len,char *out,int cap) {
 ghostty_key_encoder_setopt_from_terminal(w->encoder,w->term);
 ghostty_key_event_set_action(w->event,(GhosttyKeyAction)action);ghostty_key_event_set_key(w->event,physical_key(vk,scan));
 ghostty_key_event_set_mods(w->event,(GhosttyMods)mods);ghostty_key_event_set_utf8(w->event,utf8,len);
 ghostty_key_event_set_unshifted_codepoint(w->event,unshifted);
 ghostty_key_event_set_consumed_mods(w->event,(GhosttyMods)consumed);
 ghostty_key_event_set_composing(w->event,composing!=0);
 size_t n=0;int r=ghostty_key_encoder_encode(w->encoder,w->event,out,cap,&n);
 return (r!=GHOSTTY_SUCCESS&&r!=GHOSTTY_OUT_OF_SPACE)||n>INT_MAX?-1:(int)n;
}
API int gw_bracketed(WinTerminal *w) {
 GhosttyTerminalModeConfig m={.mode=GHOSTTY_MODE_BRACKETED_PASTE};
 return ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_MODE,&m)==GHOSTTY_SUCCESS && m.value;
}
API int gw_find(WinTerminal *w,const char *text,int len,int next) {
 ghostty_selection_gesture_reset(w->gesture,w->term);
 if(!len)ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,NULL);
 GhosttyString needle={(const uint8_t*)text,(size_t)len};
 if(ghostty_search_set(w->search,GHOSTTY_SEARCH_OPT_NEEDLE,&needle) || ghostty_search_run(w->search))return -1;
 size_t count=0;ghostty_search_get(w->search,GHOSTTY_SEARCH_DATA_TOTAL_MATCHES,&count);
 if(next && count) {
  ghostty_search_set(w->search,GHOSTTY_SEARCH_OPT_SELECT_NEXT,NULL);
  GhosttySelection selection=GHOSTTY_INIT_SIZED(GhosttySelection);
  if(!ghostty_search_get(w->search,GHOSTTY_SEARCH_DATA_SELECTED_MATCH,&selection)) {
   ghostty_terminal_set(w->term,GHOSTTY_TERMINAL_OPT_SELECTION,&selection);
  }
 }
 return count>2147483647?2147483647:(int)count;
}
API int gw_mouse(WinTerminal *w,int action,int button,int mods,float x,float y,int cw,int ch,int pressed,char *out,int cap) {
 bool tracking=false;ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_MOUSE_TRACKING,&tracking);
 if(!tracking)return -1;
 uint16_t cols=0,rows=0;ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_COLS,&cols);ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_ROWS,&rows);
 GhosttyMouseEncoderSize size=GHOSTTY_INIT_SIZED(GhosttyMouseEncoderSize);
 size.screen_width=cols*cw;size.screen_height=rows*ch;size.cell_width=cw;size.cell_height=ch;
 bool any=pressed!=0;ghostty_mouse_encoder_setopt_from_terminal(w->mouse_encoder,w->term);
 ghostty_mouse_encoder_setopt(w->mouse_encoder,GHOSTTY_MOUSE_ENCODER_OPT_SIZE,&size);
 ghostty_mouse_encoder_setopt(w->mouse_encoder,GHOSTTY_MOUSE_ENCODER_OPT_ANY_BUTTON_PRESSED,&any);
 ghostty_mouse_event_set_action(w->mouse_event,(GhosttyMouseAction)action);
 if(button)ghostty_mouse_event_set_button(w->mouse_event,(GhosttyMouseButton)button);else ghostty_mouse_event_clear_button(w->mouse_event);
 ghostty_mouse_event_set_mods(w->mouse_event,(GhosttyMods)mods);
 ghostty_mouse_event_set_position(w->mouse_event,(GhosttyMousePosition){x,y});
 size_t n=0;return ghostty_mouse_encoder_encode(w->mouse_encoder,w->mouse_event,out,cap,&n)?0:(int)n;
}
API int gw_focus(WinTerminal *w,int gained) {
 GhosttyTerminalModeConfig m={.mode=ghostty_mode_new(1004,false)};
 return ghostty_terminal_get(w->term,GHOSTTY_TERMINAL_DATA_MODE,&m)==GHOSTTY_SUCCESS && m.value;
}
API int gw_held(WinTerminal *w) {return w->held;}
API int gw_link(WinTerminal *w,int x,int y,char *out,int cap) {
 GhosttyGridRef ref=GHOSTTY_INIT_SIZED(GhosttyGridRef);
 GhosttyPoint p={.tag=GHOSTTY_POINT_TAG_VIEWPORT,.value.coordinate={x,y}};
 if(ghostty_terminal_grid_ref(w->term,p,&ref))return 0;
 size_t n=0;int r=ghostty_grid_ref_hyperlink_uri(&ref,(uint8_t*)out,cap,&n);
 return (r==GHOSTTY_SUCCESS||r==GHOSTTY_OUT_OF_SPACE) && n<=INT_MAX?(int)n:0;
}
